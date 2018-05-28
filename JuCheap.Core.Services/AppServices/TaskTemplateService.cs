﻿using AutoMapper;
using AutoMapper.QueryableExtensions;
using JuCheap.Core.Data;
using JuCheap.Core.Data.Entity;
using JuCheap.Core.Infrastructure;
using JuCheap.Core.Infrastructure.Enums;
using JuCheap.Core.Infrastructure.Exceptions;
using JuCheap.Core.Infrastructure.Extentions;
using JuCheap.Core.Interfaces;
using JuCheap.Core.Models;
using JuCheap.Core.Models.Filters;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JuCheap.Core.Services.AppServices
{
    /// <summary>
    /// 任务流模板服务接口
    /// </summary>
    public class TaskTemplateService : ITaskTemplateService
    {
        private readonly JuCheapContext _context;
        private readonly IMapper _mapper;
        private readonly IConfigurationProvider _configurationProvider;

        public TaskTemplateService(JuCheapContext context, IMapper mapper, IConfigurationProvider configurationProvider)
        {
            _context = context;
            _mapper = mapper;
            _configurationProvider = configurationProvider;
        }

        /// <summary>
        /// 创建任务流模板
        /// </summary>
        public async Task<string> CreateAsync(TaskTemplateDto templateDto, CurrentUserDto user)
        {
            if (templateDto.Name.IsBlank())
            {
                throw new BusinessException("模板名称不能为空");
            }
            if (templateDto.Id.IsNotBlank())
            {
                var template = await _context.TaskTemplates.FindAsync(templateDto.Id);
                if (template != null)
                {
                    template.Name = templateDto.Name;
                }
            }
            else
            {
                var template = new TaskTemplateEntity { Name = templateDto.Name };
                template.CreateBy(user.UserId);
                template.SetStep(TaskTemplateStep.Save);
                await _context.AddAsync(template);
            }
            
           
            await _context.SaveChangesAsync();
            return templateDto.Id;
        }

        /// <summary>
        /// 创建表单信息
        /// </summary>
        public async Task CreateFormsAsync(IList<TaskTemplateFormDto> forms, CurrentUserDto user)
        {
            var templateIds = forms.Select(x => x.TemplateId).Distinct().ToList();
            var templates = await _context.TaskTemplates.Where(x => templateIds.Contains(x.Id)).ToListAsync();
            templates.ForEach(x => x.SetStep(TaskTemplateStep.DesignForms));
            var templateForms = await _context.TaskTemplateForms.Where(x => templateIds.Contains(x.TemplateId)).ToListAsync();
            if (templateForms.AnyOne())
            {
                _context.TaskTemplateForms.RemoveRange(templateForms);
            }
            var formList = _mapper.Map<List<TaskTemplateFormEntity>>(forms);
            var order = 1;
            formList.ForEach(x =>
            {
                x.CreateBy(user.UserId);
                x.Order = order;
                order++;
            });
            await _context.TaskTemplateForms.AddRangeAsync(formList);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 创建步骤操作信息
        /// </summary>
        public async Task CreateStepsAsync(IList<TaskTemplateStepDto> steps, CurrentUserDto user)
        {
            var templateIds = steps.Select(x => x.TemplateId).Distinct().ToList();
            var templates = await _context.TaskTemplates.Where(x => templateIds.Contains(x.Id)).ToListAsync();
            templates.ForEach(x => x.SetStep(TaskTemplateStep.DesignSteps));
            //删除以前的数据
            var stepIds = steps.Select(x => x.Id).Distinct().ToList();
            var operates = await _context.TaskTemplateStepOperates.Where(x => stepIds.Contains(x.StepId)).ToListAsync();
            _context.TaskTemplateStepOperates.RemoveRange(operates);
            var oldSteps = await _context.TaskTemplateSteps.Where(x => stepIds.Contains(x.Id)).ToListAsync();
            _context.TaskTemplateSteps.RemoveRange(oldSteps);
            var list = _mapper.Map<List<TaskTemplateStepEntity>>(steps);
            list.ForEach(x =>
            {
                x.Order += 1;
                x.Operates = x.Operates.Where(o => o.Name.IsNotBlank()).ToList();
                x.CreateBy(user.UserId);
                x.Operates.ForEach(m =>
                {
                    m.StepId = x.Id;
                    m.CreateBy(user.UserId);
                });
            });
            await _context.TaskTemplateSteps.AddRangeAsync(list);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 获取任务模板列表
        /// </summary>
        public async Task<PagedResult<TaskTemplateDto>> SearchAsync(BaseFilter filters)
        {
            var query = _context.TaskTemplates
                .WhereIf(filters.keywords.IsNotBlank(), x => x.Name.Contains(filters.keywords));

            return await query.OrderByDescending(x => x.CreateDateTime)
                .ProjectTo<TaskTemplateDto>(_configurationProvider)
                .PagingAsync(filters.page, filters.rows);
        }

        /// <summary>
        /// 获取任务模板的信息
        /// </summary>
        public async Task<TaskTemplateDto> GetTemplateAsync(string templateId)
        {
            if (templateId.IsBlank())
            {
                return null;
            }
            var query = _context.TaskTemplates.Where(x => x.Id == templateId);
            return await query.ProjectTo<TaskTemplateDto>(_configurationProvider).FirstOrDefaultAsync();
        }

        /// <summary>
        /// 获取模板的表单信息
        /// </summary>
        public async Task<IList<TaskTemplateFormDto>> GetFormsAsync(string templateId)
        {
            var query = _context.TaskTemplateForms
                .OrderBy(x => x.Order)
                .Where(x => x.TemplateId == templateId);
            return await query.ProjectTo<TaskTemplateFormDto>(_configurationProvider).ToListAsync();
        }

        /// <summary>
        /// 获取模板的步骤信息
        /// </summary>
        public async Task<IList<TaskTemplateStepDto>> GetStepsAsync(string templateId)
        {
            var query = _context.TaskTemplateSteps
                .Include(x => x.Operates)
                .OrderBy(x => x.Order)
                .Where(x => x.TemplateId == templateId);
            return await query.ProjectTo<TaskTemplateStepDto>(_configurationProvider).ToListAsync();
        }
    }
}
